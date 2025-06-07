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
    /// Główne endpointy dla masowych operacji archiwizacji, przywracania i migracji
    /// Następuje wzorce z SchoolYearProcessController
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
        /// <param name="request">Parametry archiwizacji</param>
        /// <returns>Wynik operacji masowej</returns>
        [HttpPost("bulk-archive")]
        public async Task<IActionResult> BulkArchiveTeamsWithCleanup([FromBody] BulkArchiveRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masową archiwizację {Count} zespołów", request.TeamIds?.Length ?? 0);
                
                // Pobierz token dostępu z nagłówka Authorization
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                // Pobierz UPN użytkownika z claims
                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                // Pobierz token Graph przez OBO flow
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
        /// <param name="request">Parametry przywracania</param>
        /// <returns>Wynik operacji masowej</returns>
        [HttpPost("bulk-restore")]
        public async Task<IActionResult> BulkRestoreTeamsWithValidation([FromBody] BulkRestoreRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masowe przywracanie {Count} zespołów", request.TeamIds?.Length ?? 0);
                
                // Pobierz token dostępu z nagłówka Authorization
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                // Pobierz UPN użytkownika z claims
                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                // Pobierz token Graph przez OBO flow
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
        /// <param name="request">Plan migracji</param>
        /// <returns>Wynik operacji migracji</returns>
        [HttpPost("migrate")]
        public async Task<IActionResult> MigrateTeamsBetweenSchoolYears([FromBody] TeamMigrationRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam migrację {Count} zespołów z {From} do {To}", 
                    request.Plan?.TeamIds?.Length ?? 0, request.Plan?.FromSchoolYearId, request.Plan?.ToSchoolYearId);
                
                // Pobierz token dostępu z nagłówka Authorization
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                // Pobierz UPN użytkownika z claims
                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                // Pobierz token Graph przez OBO flow
                var accessToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Unauthorized("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
                }

                var result = await _orchestrator.MigrateTeamsBetweenSchoolYearsAsync(request.Plan, accessToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("✅ API: Migracja zespołów zakończona sukcesem. Sukcesy: {Success}, Błędy: {Errors}", 
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
                    _logger.LogWarning("⚠️ API: Migracja zespołów zakończona z błędami: {ErrorMessage}", result.ErrorMessage);
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
        /// <param name="request">Opcje konsolidacji</param>
        /// <returns>Wynik operacji konsolidacji</returns>
        [HttpPost("consolidate")]
        public async Task<IActionResult> ConsolidateInactiveTeams([FromBody] ConsolidationRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam konsolidację nieaktywnych zespołów");
                
                // Pobierz token dostępu z nagłówka Authorization
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Brak tokenu dostępu w nagłówku Authorization");
                }
                var apiAccessToken = authHeader.Substring("Bearer ".Length).Trim();

                // Pobierz UPN użytkownika z claims
                var userUpn = User.FindFirst("upn")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userUpn))
                {
                    return Unauthorized("Nie można określić tożsamości użytkownika");
                }

                // Pobierz token Graph przez OBO flow
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
        /// Pobiera status aktywnych procesów cyklu życia zespołów
        /// </summary>
        /// <returns>Lista aktywnych procesów</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetActiveProcessesStatus()
        {
            try
            {
                _logger.LogDebug("🔍 API: Pobieranie statusu aktywnych procesów cyklu życia");
                
                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                
                _logger.LogInformation("✅ API: Znaleziono {Count} aktywnych procesów cyklu życia", processes.Count());
                return Ok(new ProcessStatusResponse
                {
                    Success = true,
                    Message = $"Znaleziono {processes.Count()} aktywnych procesów",
                    Processes = processes.ToArray()
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
        /// Anuluje aktywny proces cyklu życia zespołów
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>Wynik anulowania</returns>
        [HttpDelete("{processId}")]
        public async Task<IActionResult> CancelProcess(string processId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processId))
                {
                    return BadRequest(new CancelProcessResponse 
                    { 
                        Success = false, 
                        Message = "ID procesu jest wymagane" 
                    });
                }

                _logger.LogInformation("🔄 API: Próba anulowania procesu cyklu życia {ProcessId}", processId);
                
                var success = await _orchestrator.CancelProcessAsync(processId);
                
                if (success)
                {
                    _logger.LogInformation("✅ API: Proces {ProcessId} został anulowany", processId);
                    return Ok(new CancelProcessResponse 
                    { 
                        Success = true, 
                        Message = "Proces został anulowany" 
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Nie można anulować procesu {ProcessId} - proces nie istnieje lub już się zakończył", processId);
                    return NotFound(new CancelProcessResponse 
                    { 
                        Success = false, 
                        Message = "Proces nie istnieje lub już się zakończył" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas anulowania procesu {ProcessId}", processId);
                return StatusCode(500, new CancelProcessResponse 
                { 
                    Success = false, 
                    Message = "Wystąpił błąd wewnętrzny serwera" 
                });
            }
        }
    }

    #region Request/Response DTOs

    /// <summary>
    /// Request do masowej archiwizacji zespołów
    /// </summary>
    public class BulkArchiveRequest
    {
        /// <summary>
        /// Lista ID zespołów do archiwizacji
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "Wymagany jest przynajmniej jeden zespół")]
        public string[] TeamIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Opcje archiwizacji
        /// </summary>
        [Required]
        public ArchiveOptions Options { get; set; } = new ArchiveOptions();
    }

    /// <summary>
    /// Request do masowego przywracania zespołów
    /// </summary>
    public class BulkRestoreRequest
    {
        /// <summary>
        /// Lista ID zespołów do przywrócenia
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "Wymagany jest przynajmniej jeden zespół")]
        public string[] TeamIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Opcje przywracania
        /// </summary>
        [Required]
        public RestoreOptions Options { get; set; } = new RestoreOptions();
    }

    /// <summary>
    /// Request do migracji zespołów
    /// </summary>
    public class TeamMigrationRequest
    {
        /// <summary>
        /// Plan migracji zespołów
        /// </summary>
        [Required]
        public TeamMigrationPlan Plan { get; set; } = new TeamMigrationPlan();
    }

    /// <summary>
    /// Request do konsolidacji nieaktywnych zespołów
    /// </summary>
    public class ConsolidationRequest
    {
        /// <summary>
        /// Opcje konsolidacji
        /// </summary>
        [Required]
        public ConsolidationOptions Options { get; set; } = new ConsolidationOptions();
    }

    /// <summary>
    /// Odpowiedź dla operacji masowych
    /// </summary>
    public class BulkOperationResponse
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat wyniku
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Szczegółowy wynik operacji
        /// </summary>
        public BulkOperationResult? Result { get; set; }
    }

    /// <summary>
    /// Odpowiedź dla statusu procesów
    /// </summary>
    public class ProcessStatusResponse
    {
        /// <summary>
        /// Czy zapytanie się powiodło
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat wyniku
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Lista aktywnych procesów
        /// </summary>
        public TeamLifecycleProcessStatus[] Processes { get; set; } = Array.Empty<TeamLifecycleProcessStatus>();
    }



    #endregion
} 