using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Api.Extensions;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler do diagnostyki systemu PowerShell/Graph
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IPowerShellConnectionService _powerShellConnectionService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            IPowerShellConnectionService powerShellConnectionService,
            ILogger<DiagnosticsController> logger)
        {
            _powerShellConnectionService = powerShellConnectionService ?? throw new ArgumentNullException(nameof(powerShellConnectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Wykonuje podstawową diagnostykę połączenia PowerShell/Graph
        /// </summary>
        /// <returns>Informacje diagnostyczne</returns>
        [HttpGet("connection")]
        public async Task<ActionResult<PowerShellDiagnosticInfo>> DiagnoseConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie diagnostyki połączenia PowerShell/Graph");
                
                var diagnostic = await _powerShellConnectionService.DiagnoseConnectionAsync();
                
                _logger.LogInformation("Diagnostyka zakończona. Stan: {OverallHealth}", diagnostic.OverallHealth);
                
                return Ok(diagnostic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas diagnostyki połączenia");
                return StatusCode(500, new { error = "Błąd podczas diagnostyki połączenia", details = ex.Message });
            }
        }

        /// <summary>
        /// Wykonuje rozszerzoną diagnostykę z testowaniem konkretnych komend
        /// </summary>
        /// <param name="testCommands">Lista komend do przetestowania</param>
        /// <param name="includePermissions">Czy sprawdzić uprawnienia</param>
        /// <returns>Szczegółowe informacje diagnostyczne</returns>
        [HttpPost("connection/extended")]
        public async Task<ActionResult<PowerShellDiagnosticInfo>> DiagnoseConnectionExtendedAsync(
            [FromBody] string[]? testCommands = null,
            [FromQuery] bool includePermissions = true)
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie rozszerzonej diagnostyki PowerShell/Graph");
                
                var commands = testCommands ?? new[] { "Get-MgUser -Top 1", "Get-MgGroup -Top 1" };
                var diagnostic = await _powerShellConnectionService.DiagnoseConnectionAsync(includePermissions, commands);
                
                _logger.LogInformation("Rozszerzona diagnostyka zakończona. Stan: {OverallHealth}", diagnostic.OverallHealth);
                
                return Ok(diagnostic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas rozszerzonej diagnostyki");
                return StatusCode(500, new { error = "Błąd podczas rozszerzonej diagnostyki", details = ex.Message });
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia dla konkretnych operacji
        /// </summary>
        /// <param name="permissions">Lista uprawnień do sprawdzenia</param>
        /// <returns>Informacje o uprawnieniach</returns>
        [HttpPost("permissions")]
        public async Task<ActionResult<PowerShellPermissionInfo>> ValidatePermissionsAsync([FromBody] string[] permissions)
        {
            try
            {
                if (permissions == null || !permissions.Any())
                {
                    return BadRequest(new { error = "Lista uprawnień nie może być pusta" });
                }

                _logger.LogInformation("Sprawdzanie uprawnień: {Permissions}", string.Join(", ", permissions));
                
                var permissionInfo = await _powerShellConnectionService.ValidatePermissionsAsync(permissions);
                
                _logger.LogInformation("Sprawdzenie uprawnień zakończone. IsValid: {IsValid}", permissionInfo.IsValid);
                
                return Ok(permissionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania uprawnień", details = ex.Message });
            }
        }

        /// <summary>
        /// Pobiera podstawowe informacje o stanie połączenia
        /// </summary>
        /// <returns>Informacje o stanie połączenia</returns>
        [HttpGet("health")]
        public async Task<ActionResult<ConnectionHealthInfo>> GetConnectionHealthAsync()
        {
            try
            {
                var healthInfo = await _powerShellConnectionService.GetConnectionHealthAsync();
                return Ok(healthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania informacji o stanie połączenia");
                return StatusCode(500, new { error = "Błąd podczas pobierania stanu połączenia", details = ex.Message });
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia wymagane do tworzenia użytkowników
        /// </summary>
        /// <returns>Informacje o uprawnieniach do tworzenia użytkowników</returns>
        [HttpGet("permissions/user-creation")]
        public async Task<ActionResult<PowerShellPermissionInfo>> ValidateUserCreationPermissionsAsync()
        {
            try
            {
                var requiredPermissions = new[] 
                { 
                    "User.ReadWrite.All", 
                    "Directory.ReadWrite.All",
                    "Group.ReadWrite.All"
                };

                var permissionInfo = await _powerShellConnectionService.ValidatePermissionsAsync(requiredPermissions);
                return Ok(permissionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień do tworzenia użytkowników");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania uprawnień", details = ex.Message });
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia wymagane do zarządzania zespołami
        /// </summary>
        /// <returns>Informacje o uprawnieniach do zarządzania zespołami</returns>
        [HttpGet("permissions/team-management")]
        public async Task<ActionResult<PowerShellPermissionInfo>> ValidateTeamManagementPermissionsAsync()
        {
            try
            {
                var requiredPermissions = new[] 
                { 
                    "Group.ReadWrite.All", 
                    "Directory.ReadWrite.All",
                    "TeamMember.ReadWrite.All"
                };

                var permissionInfo = await _powerShellConnectionService.ValidatePermissionsAsync(requiredPermissions);
                return Ok(permissionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień do zarządzania zespołami");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania uprawnień", details = ex.Message });
            }
        }

        /// <summary>
        /// Testuje wykonanie konkretnej operacji PowerShell
        /// </summary>
        /// <param name="operationName">Nazwa operacji do przetestowania</param>
        /// <param name="requiredPermissions">Wymagane uprawnienia</param>
        /// <returns>Wynik testu operacji</returns>
        [HttpPost("test-operation")]
        public async Task<ActionResult<object>> TestOperationAsync(
            [FromQuery] string operationName,
            [FromBody] string[]? requiredPermissions = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    return BadRequest(new { error = "Nazwa operacji jest wymagana" });
                }

                _logger.LogInformation("Testowanie operacji PowerShell: {OperationName}", operationName);

                var permissions = requiredPermissions ?? Array.Empty<string>();
                
                // Sprawdź uprawnienia jeśli podano
                PowerShellPermissionInfo? permissionInfo = null;
                if (permissions.Any())
                {
                    permissionInfo = await _powerShellConnectionService.ValidatePermissionsAsync(permissions);
                    if (!permissionInfo.IsValid)
                    {
                        return Ok(new
                        {
                            OperationName = operationName,
                            CanExecute = false,
                            Reason = "Insufficient permissions",
                            PermissionInfo = permissionInfo
                        });
                    }
                }

                // Sprawdź podstawowy stan połączenia
                var healthInfo = await _powerShellConnectionService.GetConnectionHealthAsync();
                if (!healthInfo.IsConnected)
                {
                    return Ok(new
                    {
                        OperationName = operationName,
                        CanExecute = false,
                        Reason = "Not connected to Microsoft Graph",
                        HealthInfo = healthInfo
                    });
                }

                // Wykonaj test operacji
                var testResult = await _powerShellConnectionService.ExecuteScriptAsync("Get-Date");

                var canExecute = testResult != null;

                return Ok(new
                {
                    OperationName = operationName,
                    CanExecute = canExecute,
                    Reason = canExecute ? "Operation can be executed" : "Operation failed",
                    PermissionInfo = permissionInfo,
                    HealthInfo = healthInfo,
                    TestTimestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testowania operacji {OperationName}", operationName);
                return StatusCode(500, new { error = "Błąd podczas testowania operacji", details = ex.Message });
            }
        }

        /// <summary>
        /// Pobiera pełny raport diagnostyczny systemu
        /// </summary>
        /// <returns>Kompleksowy raport diagnostyczny</returns>
        [HttpGet("full-report")]
        public async Task<ActionResult<object>> GetFullDiagnosticReportAsync()
        {
            try
            {
                _logger.LogInformation("Generowanie pełnego raportu diagnostycznego");

                var healthTask = _powerShellConnectionService.GetConnectionHealthAsync();
                var diagnosticTask = _powerShellConnectionService.DiagnoseConnectionAsync(true, "Get-MgUser -Top 1", "Get-MgGroup -Top 1");
                var permissionTask = _powerShellConnectionService.ValidatePermissionsAsync("User.ReadWrite.All", "Group.ReadWrite.All", "Directory.ReadWrite.All");

                await Task.WhenAll(healthTask, diagnosticTask, permissionTask);

                var healthInfo = await healthTask;
                var diagnostic = await diagnosticTask;
                var permissionInfo = await permissionTask;

                var report = new
                {
                    GeneratedAt = DateTime.UtcNow,
                    OverallStatus = diagnostic.OverallHealth.ToString(),
                    Summary = new
                    {
                        IsHealthy = diagnostic.OverallHealth == PowerShellHealthStatus.Healthy,
                        HasCriticalIssues = diagnostic.OverallHealth == PowerShellHealthStatus.Critical,
                        ErrorCount = diagnostic.Errors.Count,
                        PermissionsValid = permissionInfo.IsValid
                    },
                    ConnectionHealth = healthInfo,
                    DetailedDiagnostic = diagnostic,
                    Permissions = permissionInfo,
                    Recommendations = GenerateRecommendations(diagnostic, permissionInfo)
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas generowania pełnego raportu diagnostycznego");
                return StatusCode(500, new { error = "Błąd podczas generowania raportu", details = ex.Message });
            }
        }

        /// <summary>
        /// Generuje rekomendacje na podstawie diagnostyki
        /// </summary>
        private List<string> GenerateRecommendations(PowerShellDiagnosticInfo diagnostic, PowerShellPermissionInfo permissionInfo)
        {
            var recommendations = new List<string>();

            if (!diagnostic.RunspaceReady)
            {
                recommendations.Add("Sprawdź konfigurację środowiska PowerShell");
            }

            if (!diagnostic.IsConnected)
            {
                recommendations.Add("Sprawdź połączenie z Microsoft Graph");
            }

            if (!diagnostic.BasicCommandTest)
            {
                recommendations.Add("Sprawdź podstawową funkcjonalność PowerShell");
            }

            if (!diagnostic.GraphConnectionTest)
            {
                recommendations.Add("Sprawdź moduł Microsoft Graph PowerShell");
            }

            if (!permissionInfo.IsValid)
            {
                recommendations.Add("Sprawdź uprawnienia aplikacji w Azure AD");
            }

            if (diagnostic.Errors.Any())
            {
                recommendations.Add("Przejrzyj szczegóły błędów w sekcji diagnostycznej");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("System działa prawidłowo");
            }

            return recommendations;
        }

        /// <summary>
        /// Sprawdza status instalacji wymaganych modułów PowerShell
        /// </summary>
        /// <returns>Status modułów PowerShell</returns>
        [HttpGet("modules/status")]
        [AllowAnonymous]
        public async Task<ActionResult<PowerShellModuleStatus>> GetModuleStatusAsync()
        {
            try
            {
                _logger.LogInformation("Sprawdzanie statusu modułów PowerShell");
                
                var moduleStatus = await _powerShellConnectionService.CheckModuleInstallationAsync();
                
                _logger.LogInformation("Status modułów sprawdzony. Status: {OverallStatus}, Zainstalowane: {InstalledCount}/{RequiredCount}",
                    moduleStatus.OverallStatus, moduleStatus.InstalledModulesCount, moduleStatus.RequiredModulesCount);
                
                return Ok(moduleStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania statusu modułów");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania statusu modułów", details = ex.Message });
            }
        }

        /// <summary>
        /// Instaluje wymagane moduły PowerShell
        /// </summary>
        /// <param name="forceReinstall">Czy wymusić reinstalację istniejących modułów</param>
        /// <returns>Wynik instalacji modułów</returns>
        [HttpPost("modules/install")]
        [AllowAnonymous]
        public async Task<ActionResult<PowerShellModuleInstallationResult>> InstallModulesAsync([FromQuery] bool forceReinstall = false)
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie instalacji modułów PowerShell (Force: {ForceReinstall})", forceReinstall);
                
                var installationResult = await _powerShellConnectionService.InstallRequiredModulesAsync(forceReinstall);
                
                _logger.LogInformation("Instalacja modułów zakończona. Sukces: {Success}", installationResult.Success);
                
                return Ok(installationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas instalacji modułów");
                return StatusCode(500, new { error = "Błąd podczas instalacji modułów", details = ex.Message });
            }
        }

        /// <summary>
        /// Wykonuje kompleksowy test połączenia z Microsoft Graph
        /// </summary>
        /// <returns>Wynik testu połączenia</returns>
        [HttpPost("connection/test")]
        [AllowAnonymous]
        public async Task<ActionResult<PowerShellConnectionTestResult>> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie testu połączenia Graph");
                
                var testResult = await _powerShellConnectionService.TestGraphConnectionAsync();
                
                _logger.LogInformation("Test połączenia zakończony. Wynik: {OverallResult}, Testy przeszły: {PassedTests}/{TotalTests}",
                    testResult.OverallResult, testResult.PassedTestsCount, testResult.TotalTestsCount);
                
                return Ok(testResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testu połączenia");
                return StatusCode(500, new { error = "Błąd podczas testu połączenia", details = ex.Message });
            }
        }
    }
} 