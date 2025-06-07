using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.Api.Extensions;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler API dla orkiestratora monitorowania zdrowia systemu
    /// Główne endpointy dla operacji diagnostycznych, naprawy automatycznej i optymalizacji
    /// Następuje wzorce z SchoolYearProcessController, TeamLifecycleController i BulkUserManagementController
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class HealthMonitoringController : ControllerBase
    {
        private readonly IHealthMonitoringOrchestrator _orchestrator;
        private readonly ITokenManager _tokenManager;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<HealthMonitoringController> _logger;

        public HealthMonitoringController(
            IHealthMonitoringOrchestrator orchestrator,
            ITokenManager tokenManager,
            ICurrentUserService currentUserService,
            ILogger<HealthMonitoringController> logger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Przeprowadza kompleksowe sprawdzenie zdrowia wszystkich komponentów systemu
        /// </summary>
        /// <returns>Wynik kompleksowego sprawdzenia zdrowia systemu</returns>
        /// <response code="200">Sprawdzenie zdrowia zakończone pomyślnie</response>
        /// <response code="400">Błędne parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("comprehensive-health-check")]
        [ProducesResponseType(typeof(HealthOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<HealthOperationResult>> RunComprehensiveHealthCheck()
        {
            try
            {
                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                if (string.IsNullOrWhiteSpace(currentUserUpn))
                {
                    _logger.LogWarning("[HealthMonitoring] Nie można określić UPN aktualnego użytkownika");
                    return BadRequest("Nie można określić tożsamości użytkownika");
                }

                _logger.LogInformation("[HealthMonitoring] Rozpoczynanie kompleksowego sprawdzenia zdrowia przez użytkownika {UserUpn}", currentUserUpn);

                // Pobierz token z nagłówka Authorization
                var apiToken = await HttpContext.GetBearerTokenAsync();
                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu API dla {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                // Pobierz token OBO
                var oboToken = await _tokenManager.GetValidAccessTokenAsync(currentUserUpn, apiToken);
                if (string.IsNullOrWhiteSpace(oboToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu OBO dla {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                var result = await _orchestrator.RunComprehensiveHealthCheckAsync(oboToken);

                if (result.Success)
                {
                    _logger.LogInformation("[HealthMonitoring] Kompleksowe sprawdzenie zdrowia zakończone sukcesem dla {UserUpn}. " +
                        "Komponenty: {ComponentCount}, Rekomendacje: {RecommendationCount}", 
                        currentUserUpn, result.HealthChecks.Count, result.Recommendations.Count);
                }
                else
                {
                    _logger.LogWarning("[HealthMonitoring] Kompleksowe sprawdzenie zdrowia wykryło problemy dla {UserUpn}. " +
                        "Błędy: {ErrorCount}, Sukces: {SuccessCount}", 
                        currentUserUpn, result.Errors.Count, result.SuccessfulOperations.Count);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitoring] Błąd podczas kompleksowego sprawdzenia zdrowia");
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatyczne naprawianie typowych problemów systemowych
        /// </summary>
        /// <param name="request">Parametry procesu naprawy automatycznej</param>
        /// <returns>Wynik operacji naprawy automatycznej</returns>
        /// <response code="200">Naprawa automatyczna zakończona</response>
        /// <response code="400">Błędne parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("auto-repair")]
        [ProducesResponseType(typeof(HealthOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<HealthOperationResult>> AutoRepairCommonIssues([FromBody] AutoRepairRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Parametry żądania są wymagane");
                }

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                if (string.IsNullOrWhiteSpace(currentUserUpn))
                {
                    _logger.LogWarning("[HealthMonitoring] Nie można określić UPN aktualnego użytkownika dla auto-repair");
                    return BadRequest("Nie można określić tożsamości użytkownika");
                }

                _logger.LogInformation("[HealthMonitoring] Rozpoczynanie automatycznej naprawy przez użytkownika {UserUpn}. " +
                    "DryRun: {DryRun}, PowerShell: {PowerShell}, Cache: {Cache}", 
                    currentUserUpn, request.DryRun, request.RepairPowerShellConnection, request.ClearInvalidCache);

                // Pobierz token z nagłówka Authorization
                var apiToken = await HttpContext.GetBearerTokenAsync();
                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu API dla auto-repair {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                // Pobierz token OBO
                var oboToken = await _tokenManager.GetValidAccessTokenAsync(currentUserUpn, apiToken);
                if (string.IsNullOrWhiteSpace(oboToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu OBO dla auto-repair {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                var options = new RepairOptions
                {
                    RepairPowerShellConnection = request.RepairPowerShellConnection,
                    ClearInvalidCache = request.ClearInvalidCache,
                    RestartStuckProcesses = request.RestartStuckProcesses,
                    OptimizeDatabase = request.OptimizeDatabase,
                    SendAdminNotifications = request.SendAdminNotifications,
                    DryRun = request.DryRun,
                    TimeoutMinutes = request.TimeoutMinutes,
                    MaxConcurrency = request.MaxConcurrency
                };

                var result = await _orchestrator.AutoRepairCommonIssuesAsync(options, oboToken);

                if (result.Success)
                {
                    _logger.LogInformation("[HealthMonitoring] Automatyczna naprawa zakończona sukcesem dla {UserUpn}. " +
                        "Naprawy: {RepairCount}, Czas: {ElapsedMs}ms", 
                        currentUserUpn, result.SuccessfulOperations.Count, result.ExecutionTimeMs);
                }
                else
                {
                    _logger.LogWarning("[HealthMonitoring] Automatyczna naprawa zakończona z błędami dla {UserUpn}. " +
                        "Błędy: {ErrorCount}, Naprawy: {RepairCount}", 
                        currentUserUpn, result.Errors.Count, result.SuccessfulOperations.Count);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitoring] Błąd podczas automatycznej naprawy");
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }

        /// <summary>
        /// Synchronizacja ze stanem Microsoft Graph
        /// </summary>
        /// <returns>Wynik operacji synchronizacji z Microsoft Graph</returns>
        /// <response code="200">Synchronizacja zakończona pomyślnie</response>
        /// <response code="400">Błędne parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("graph-synchronization")]
        [ProducesResponseType(typeof(HealthOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<HealthOperationResult>> SynchronizeWithMicrosoftGraph()
        {
            try
            {
                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                if (string.IsNullOrWhiteSpace(currentUserUpn))
                {
                    _logger.LogWarning("[HealthMonitoring] Nie można określić UPN aktualnego użytkownika dla synchronizacji Graph");
                    return BadRequest("Nie można określić tożsamości użytkownika");
                }

                _logger.LogInformation("[HealthMonitoring] Rozpoczynanie synchronizacji z Microsoft Graph przez użytkownika {UserUpn}", currentUserUpn);

                // Pobierz token z nagłówka Authorization
                var apiToken = await HttpContext.GetBearerTokenAsync();
                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu API dla synchronizacji Graph {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                // Pobierz token OBO
                var oboToken = await _tokenManager.GetValidAccessTokenAsync(currentUserUpn, apiToken);
                if (string.IsNullOrWhiteSpace(oboToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu OBO dla synchronizacji Graph {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                var result = await _orchestrator.SynchronizeWithMicrosoftGraphAsync(oboToken);

                if (result.Success)
                {
                    _logger.LogInformation("[HealthMonitoring] Synchronizacja z Microsoft Graph zakończona sukcesem dla {UserUpn}. " +
                        "Operacje: {OperationCount}, Czas: {ElapsedMs}ms", 
                        currentUserUpn, result.SuccessfulOperations.Count, result.ExecutionTimeMs);
                }
                else
                {
                    _logger.LogWarning("[HealthMonitoring] Synchronizacja z Microsoft Graph zakończona z błędami dla {UserUpn}. " +
                        "Błędy: {ErrorCount}, Sukces: {SuccessCount}", 
                        currentUserUpn, result.Errors.Count, result.SuccessfulOperations.Count);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitoring] Błąd podczas synchronizacji z Microsoft Graph");
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }

        /// <summary>
        /// Optymalizacja wydajności cache systemu
        /// </summary>
        /// <returns>Wynik operacji optymalizacji cache</returns>
        /// <response code="200">Optymalizacja cache zakończona pomyślnie</response>
        /// <response code="400">Błędne parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("cache-optimization")]
        [ProducesResponseType(typeof(HealthOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<HealthOperationResult>> OptimizeCachePerformance()
        {
            try
            {
                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                if (string.IsNullOrWhiteSpace(currentUserUpn))
                {
                    _logger.LogWarning("[HealthMonitoring] Nie można określić UPN aktualnego użytkownika dla optymalizacji cache");
                    return BadRequest("Nie można określić tożsamości użytkownika");
                }

                _logger.LogInformation("[HealthMonitoring] Rozpoczynanie optymalizacji cache przez użytkownika {UserUpn}", currentUserUpn);

                // Pobierz token z nagłówka Authorization
                var apiToken = await HttpContext.GetBearerTokenAsync();
                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu API dla optymalizacji cache {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                // Pobierz token OBO
                var oboToken = await _tokenManager.GetValidAccessTokenAsync(currentUserUpn, apiToken);
                if (string.IsNullOrWhiteSpace(oboToken))
                {
                    _logger.LogError("[HealthMonitoring] Nie udało się uzyskać tokenu OBO dla optymalizacji cache {UserUpn}", currentUserUpn);
                    return BadRequest("Nie udało się uzyskać tokenu dostępu");
                }

                var result = await _orchestrator.OptimizeCachePerformanceAsync(oboToken);

                if (result.Success)
                {
                    var hitRate = result.Metrics?.CacheMetrics?.HitRate ?? 0;
                    _logger.LogInformation("[HealthMonitoring] Optymalizacja cache zakończona sukcesem dla {UserUpn}. " +
                        "Hit Rate: {HitRate:F1}%, Operacje: {OperationCount}", 
                        currentUserUpn, hitRate, result.SuccessfulOperations.Count);
                }
                else
                {
                    _logger.LogWarning("[HealthMonitoring] Optymalizacja cache zakończona z błędami dla {UserUpn}. " +
                        "Błędy: {ErrorCount}, Sukces: {SuccessCount}", 
                        currentUserUpn, result.Errors.Count, result.SuccessfulOperations.Count);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitoring] Błąd podczas optymalizacji cache");
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów monitorowania zdrowia
        /// </summary>
        /// <returns>Lista aktywnych procesów monitorowania</returns>
        /// <response code="200">Lista aktywnych procesów</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(IEnumerable<HealthMonitoringProcessStatus>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<HealthMonitoringProcessStatus>>> GetActiveProcessesStatus()
        {
            try
            {
                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[HealthMonitoring] Pobieranie statusu procesów monitorowania dla {UserUpn}", currentUserUpn);

                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                
                _logger.LogInformation("[HealthMonitoring] Zwrócono {ProcessCount} aktywnych procesów monitorowania dla {UserUpn}", 
                    processes.Count(), currentUserUpn);

                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitoring] Błąd podczas pobierania statusu procesów monitorowania");
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }

        /// <summary>
        /// Anuluje aktywny proces monitorowania zdrowia
        /// </summary>
        /// <param name="processId">Identyfikator procesu do anulowania</param>
        /// <returns>Informacja o powodzeniu anulowania</returns>
        /// <response code="200">Proces został anulowany pomyślnie</response>
        /// <response code="400">Błędne parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="404">Nie znaleziono procesu</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpDelete("{processId}")]
        [ProducesResponseType(typeof(ProcessCancelResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ProcessCancelResponse>> CancelProcess([Required] string processId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processId))
                {
                    return BadRequest("Identyfikator procesu jest wymagany");
                }

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogInformation("[HealthMonitoring] Anulowanie procesu monitorowania {ProcessId} przez użytkownika {UserUpn}", 
                    processId, currentUserUpn);

                var cancelled = await _orchestrator.CancelProcessAsync(processId);

                if (cancelled)
                {
                    _logger.LogInformation("[HealthMonitoring] Proces monitorowania {ProcessId} został anulowany przez {UserUpn}", 
                        processId, currentUserUpn);
                    
                    return Ok(ProcessCancelResponse.CreateSuccess(
                        processId, 
                        "Proces został pomyślnie anulowany", 
                        "HealthMonitoring"));
                }
                else
                {
                    _logger.LogWarning("[HealthMonitoring] Nie można anulować procesu monitorowania {ProcessId} - proces nie istnieje lub nie może być anulowany", 
                        processId);
                    
                    return NotFound(ProcessCancelResponse.CreateError(
                        processId, 
                        "Proces nie istnieje lub nie może być anulowany", 
                        "HealthMonitoring"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitoring] Błąd podczas anulowania procesu monitorowania {ProcessId}", processId);
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Model żądania automatycznej naprawy
    /// </summary>
    public class AutoRepairRequest
    {
        /// <summary>
        /// Czy naprawiać problemy z połączeniem PowerShell
        /// </summary>
        public bool RepairPowerShellConnection { get; set; } = true;

        /// <summary>
        /// Czy czyścić nieważne wpisy cache
        /// </summary>
        public bool ClearInvalidCache { get; set; } = true;

        /// <summary>
        /// Czy próbować restartować zawieszenie procesy
        /// </summary>
        public bool RestartStuckProcesses { get; set; } = true;

        /// <summary>
        /// Czy optymalizować bazę danych
        /// </summary>
        public bool OptimizeDatabase { get; set; } = false;

        /// <summary>
        /// Czy wysyłać powiadomienia administratorom
        /// </summary>
        public bool SendAdminNotifications { get; set; } = true;

        /// <summary>
        /// Czy symulować operacje (dry run) bez rzeczywistych zmian
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach, domyślnie 30)
        /// </summary>
        [Range(1, 240)]
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Maksymalna liczba równoległych operacji naprawy
        /// </summary>
        [Range(1, 10)]
        public int MaxConcurrency { get; set; } = 2;
    }


} 