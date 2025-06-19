using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;

namespace TeamsManager.API.Controllers
{
    /// <summary>
    /// Kontroler API dla orkiestratora cyklu życia zespołów
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeamLifecycleController : ControllerBase
    {
        private readonly ITeamLifecycleOrchestrator _orchestrator;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<TeamLifecycleController> _logger;

        public TeamLifecycleController(
            ITeamLifecycleOrchestrator orchestrator,
            ITokenManager tokenManager,
            ILogger<TeamLifecycleController> logger)
        {
            _orchestrator = orchestrator;
            _tokenManager = tokenManager;
            _logger = logger;
        }

        /// <summary>
        /// Masowa archiwizacja zespołów z opcjonalnym cleanup
        /// </summary>
        [HttpPost("bulk-archive")]
        public async Task<IActionResult> BulkArchiveTeamsWithCleanup([FromBody] BulkArchiveRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masową archiwizację {Count} zespołów", request.TeamIds?.Length ?? 0);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                var accessToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Unauthorized("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
                }

                var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync(
                    request.TeamIds, 
                    request.Options, 
                    accessToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("✅ API: Masowa archiwizacja zakończona sukcesem. Sukcesy: {Success}, Błędy: {Errors}", 
                        result.SuccessfulOperations?.Count ?? 0, result.Errors?.Count ?? 0);
                    return Ok(new BulkOperationResponse
                    {
                        Success = true,
                        Message = $"Archiwizacja zakończona. Sukcesy: {result.SuccessfulOperations?.Count ?? 0}, Błędy: {result.Errors?.Count ?? 0}",
                        Result = result
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Masowa archiwizacja zakończona z błędami: {ErrorMessage}", result.ErrorMessage);
                    return BadRequest(new BulkOperationResponse
                    {
                        Success = false,
                        Message = result.ErrorMessage ?? "Wystąpiły błędy podczas archiwizacji",
                        Result = result
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas masowej archiwizacji zespołów");
                return StatusCode(500, new BulkOperationResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }

        /// <summary>
        /// Masowe przywracanie zespołów z walidacją
        /// </summary>
        [HttpPost("bulk-restore")]
        public async Task<IActionResult> BulkRestoreTeamsWithValidation([FromBody] BulkRestoreRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masowe przywracanie {Count} zespołów", request.TeamIds?.Length ?? 0);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                var accessToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Unauthorized("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
                }

                var result = await _orchestrator.BulkRestoreTeamsWithValidationAsync(
                    request.TeamIds, 
                    request.Options, 
                    accessToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("✅ API: Masowe przywracanie zakończone sukcesem. Sukcesy: {Success}, Błędy: {Errors}", 
                        result.SuccessfulOperations?.Count ?? 0, result.Errors?.Count ?? 0);
                    return Ok(new BulkOperationResponse
                    {
                        Success = true,
                        Message = $"Przywracanie zakończone. Sukcesy: {result.SuccessfulOperations?.Count ?? 0}, Błędy: {result.Errors?.Count ?? 0}",
                        Result = result
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Masowe przywracanie zakończone z błędami: {ErrorMessage}", result.ErrorMessage);
                    return BadRequest(new BulkOperationResponse
                    {
                        Success = false,
                        Message = result.ErrorMessage ?? "Wystąpiły błędy podczas przywracania",
                        Result = result
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas masowego przywracania zespołów");
                return StatusCode(500, new BulkOperationResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }

        /// <summary>
        /// Migracja zespołów między latami szkolnymi
        /// </summary>
        [HttpPost("migrate")]
        public async Task<IActionResult> MigrateTeamsBetweenSchoolYears([FromBody] TeamMigrationRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam migrację {Count} zespołów z {From} do {To}", 
                    request.Plan?.TeamIds?.Length ?? 0, request.Plan?.FromSchoolYearId, request.Plan?.ToSchoolYearId);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                var accessToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Unauthorized("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
                }

                var result = await _orchestrator.MigrateTeamsBetweenSchoolYearsAsync(request.Plan, accessToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("✅ API: Migracja zakończona sukcesem. Sukcesy: {Success}, Błędy: {Errors}", 
                        result.SuccessfulOperations?.Count ?? 0, result.Errors?.Count ?? 0);
                    return Ok(new BulkOperationResponse
                    {
                        Success = true,
                        Message = $"Migracja zakończona. Sukcesy: {result.SuccessfulOperations?.Count ?? 0}, Błędy: {result.Errors?.Count ?? 0}",
                        Result = result
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Migracja zakończona z błędami: {ErrorMessage}", result.ErrorMessage);
                    return BadRequest(new BulkOperationResponse
                    {
                        Success = false,
                        Message = result.ErrorMessage ?? "Wystąpiły błędy podczas migracji",
                        Result = result
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas migracji zespołów");
                return StatusCode(500, new BulkOperationResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }

        /// <summary>
        /// Konsolidacja nieaktywnych zespołów
        /// </summary>
        [HttpPost("consolidate")]
        public async Task<IActionResult> ConsolidateInactiveTeams([FromBody] ConsolidationRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam konsolidację nieaktywnych zespołów");
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                var accessToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Unauthorized("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
                }

                var result = await _orchestrator.ConsolidateInactiveTeamsAsync(request.Options, accessToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("✅ API: Konsolidacja zakończona sukcesem. Sukcesy: {Success}, Błędy: {Errors}", 
                        result.SuccessfulOperations?.Count ?? 0, result.Errors?.Count ?? 0);
                    return Ok(new BulkOperationResponse
                    {
                        Success = true,
                        Message = $"Konsolidacja zakończona. Sukcesy: {result.SuccessfulOperations?.Count ?? 0}, Błędy: {result.Errors?.Count ?? 0}",
                        Result = result
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Konsolidacja zakończona z błędami: {ErrorMessage}", result.ErrorMessage);
                    return BadRequest(new BulkOperationResponse
                    {
                        Success = false,
                        Message = result.ErrorMessage ?? "Wystąpiły błędy podczas konsolidacji",
                        Result = result
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas konsolidacji nieaktywnych zespołów");
                return StatusCode(500, new BulkOperationResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }

        /// <summary>
        /// Status aktywnych procesów
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetActiveProcessesStatus()
        {
            try
            {
                _logger.LogInformation("✅ API: Pobieranie statusu aktywnych procesów");
                
                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                
                return Ok(new ProcessStatusResponse
                {
                    Success = true,
                    Message = $"Znaleziono {processes?.Length ?? 0} aktywnych procesów",
                    Processes = processes ?? Array.Empty<TeamLifecycleProcessStatus>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas pobierania statusu procesów");
                return StatusCode(500, new ProcessStatusResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }

        /// <summary>
        /// Anulowanie procesu
        /// </summary>
        [HttpDelete("{processId}")]
        public async Task<IActionResult> CancelProcess(string processId)
        {
            try
            {
                _logger.LogInformation("✅ API: Anulowanie procesu {ProcessId}", processId);
                
                var success = await _orchestrator.CancelProcessAsync(processId);
                
                if (success)
                {
                    _logger.LogInformation("✅ API: Proces {ProcessId} anulowany pomyślnie", processId);
                    return Ok(new BulkOperationResponse
                    {
                        Success = true,
                        Message = $"Proces {processId} został anulowany pomyślnie"
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Nie udało się anulować procesu {ProcessId}", processId);
                    return BadRequest(new BulkOperationResponse
                    {
                        Success = false,
                        Message = $"Nie udało się anulować procesu {processId}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas anulowania procesu {ProcessId}", processId);
                return StatusCode(500, new BulkOperationResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }
    }

    public class BulkArchiveRequest
    {
        [Required]
        public string[] TeamIds { get; set; } = Array.Empty<string>();
        
        [Required]
        public ArchiveOptions Options { get; set; } = new ArchiveOptions();
    }

    public class BulkRestoreRequest
    {
        [Required]
        public string[] TeamIds { get; set; } = Array.Empty<string>();
        
        [Required]
        public RestoreOptions Options { get; set; } = new RestoreOptions();
    }

    public class TeamMigrationRequest
    {
        [Required]
        public TeamMigrationPlan Plan { get; set; } = new TeamMigrationPlan();
    }

    public class ConsolidationRequest
    {
        [Required]
        public ConsolidationOptions Options { get; set; } = new ConsolidationOptions();
    }

    public class BulkOperationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public BulkOperationResult? Result { get; set; }
    }

    public class ProcessStatusResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TeamLifecycleProcessStatus[] Processes { get; set; } = Array.Empty<TeamLifecycleProcessStatus>();
    }
} 