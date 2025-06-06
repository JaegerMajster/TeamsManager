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
    /// Kontroler API dla procesów związanych z rokiem szkolnym
    /// Główne endpointy dla automatyzacji tworzenia zespołów
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SchoolYearProcessController : ControllerBase
    {
        private readonly ISchoolYearProcessOrchestrator _orchestrator;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<SchoolYearProcessController> _logger;

        public SchoolYearProcessController(
            ISchoolYearProcessOrchestrator orchestrator,
            ITokenManager tokenManager,
            ILogger<SchoolYearProcessController> logger)
        {
            _orchestrator = orchestrator;
            _tokenManager = tokenManager;
            _logger = logger;
        }

        /// <summary>
        /// Główny endpoint: Tworzy zespoły dla nowego roku szkolnego
        /// </summary>
        /// <param name="request">Parametry procesu tworzenia zespołów</param>
        /// <returns>Wynik operacji masowej</returns>
        [HttpPost("create-teams-for-new-school-year")]
        [ProducesResponseType(typeof(BulkOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<BulkOperationResult>> CreateTeamsForNewSchoolYear(
            [FromBody] CreateTeamsForNewSchoolYearRequest request)
        {
            try
            {
                _logger.LogInformation("🚀 API: Rozpoczynam proces tworzenia zespołów dla roku {SchoolYearId}", request.SchoolYearId);

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

                var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(
                    request.SchoolYearId,
                    request.TemplateIds,
                    accessToken,
                    request.Options);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("✅ API: Proces tworzenia zespołów zakończony pomyślnie. Utworzono {Count} zespołów", 
                        result.SuccessfulOperations.Count);
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Proces tworzenia zespołów zakończony z błędami. Błędy: {ErrorCount}", 
                        result.Errors.Count);
                }

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("⚠️ API: Nieprawidłowe parametry: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas procesu tworzenia zespołów");
                return StatusCode(500, "Wystąpił błąd wewnętrzny serwera");
            }
        }

        /// <summary>
        /// Archiwizuje zespoły z poprzedniego roku szkolnego
        /// </summary>
        /// <param name="request">Parametry procesu archiwizacji</param>
        /// <returns>Wynik operacji masowej</returns>
        [HttpPost("archive-teams-from-previous-year")]
        [ProducesResponseType(typeof(BulkOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<BulkOperationResult>> ArchiveTeamsFromPreviousYear(
            [FromBody] ArchiveTeamsRequest request)
        {
            try
            {
                _logger.LogInformation("🗃️ API: Rozpoczynam archiwizację zespołów dla roku {SchoolYearId}", request.SchoolYearId);

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

                var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(
                    request.SchoolYearId,
                    accessToken,
                    request.Options);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("⚠️ API: Nieprawidłowe parametry: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas archiwizacji zespołów");
                return StatusCode(500, "Wystąpił błąd wewnętrzny serwera");
            }
        }

        /// <summary>
        /// Kompleksowy proces przejścia na nowy rok szkolny
        /// </summary>
        /// <param name="request">Parametry procesu przejścia</param>
        /// <returns>Wynik operacji masowej</returns>
        [HttpPost("transition-to-new-school-year")]
        [ProducesResponseType(typeof(BulkOperationResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<BulkOperationResult>> TransitionToNewSchoolYear(
            [FromBody] TransitionToNewSchoolYearRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 API: Rozpoczynam proces przejścia z roku {OldYear} na {NewYear}", 
                    request.OldSchoolYearId, request.NewSchoolYearId);

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

                var result = await _orchestrator.TransitionToNewSchoolYearAsync(
                    request.OldSchoolYearId,
                    request.NewSchoolYearId,
                    request.TemplateIds,
                    accessToken,
                    request.Options);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("⚠️ API: Nieprawidłowe parametry: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas procesu przejścia na nowy rok szkolny");
                return StatusCode(500, "Wystąpił błąd wewnętrzny serwera");
            }
        }

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów
        /// </summary>
        /// <returns>Lista aktywnych procesów</returns>
        [HttpGet("active-processes")]
        [ProducesResponseType(typeof(IEnumerable<SchoolYearProcessStatus>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<SchoolYearProcessStatus>>> GetActiveProcesses()
        {
            try
            {
                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas pobierania statusu procesów");
                return StatusCode(500, "Wystąpił błąd wewnętrzny serwera");
            }
        }

        /// <summary>
        /// Anuluje aktywny proces
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>Wynik operacji anulowania</returns>
        [HttpPost("cancel-process/{processId}")]
        [ProducesResponseType(typeof(CancelProcessResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<CancelProcessResponse>> CancelProcess(string processId)
        {
            try
            {
                if (string.IsNullOrEmpty(processId))
                {
                    return BadRequest("ID procesu nie może być pusty");
                }

                _logger.LogInformation("🛑 API: Próba anulowania procesu {ProcessId}", processId);

                var success = await _orchestrator.CancelProcessAsync(processId);
                
                if (success)
                {
                    _logger.LogInformation("✅ API: Proces {ProcessId} został anulowany", processId);
                    return Ok(new CancelProcessResponse { Success = true, Message = "Proces został anulowany" });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Nie można anulować procesu {ProcessId} - proces nie istnieje lub już się zakończył", processId);
                    return NotFound(new CancelProcessResponse { Success = false, Message = "Proces nie istnieje lub już się zakończył" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas anulowania procesu {ProcessId}", processId);
                return StatusCode(500, new CancelProcessResponse { Success = false, Message = "Wystąpił błąd wewnętrzny serwera" });
            }
        }
    }

    #region Request/Response DTOs

    /// <summary>
    /// Request do tworzenia zespołów dla nowego roku szkolnego
    /// </summary>
    public class CreateTeamsForNewSchoolYearRequest
    {
        /// <summary>
        /// ID roku szkolnego
        /// </summary>
        [Required]
        public string SchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// Lista ID szablonów zespołów
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "Wymagany jest przynajmniej jeden szablon")]
        public string[] TemplateIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Opcjonalne ustawienia procesu
        /// </summary>
        public SchoolYearProcessOptions? Options { get; set; }
    }

    /// <summary>
    /// Request do archiwizacji zespołów
    /// </summary>
    public class ArchiveTeamsRequest
    {
        /// <summary>
        /// ID roku szkolnego do archiwizacji
        /// </summary>
        [Required]
        public string SchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// Opcjonalne ustawienia procesu
        /// </summary>
        public SchoolYearProcessOptions? Options { get; set; }
    }

    /// <summary>
    /// Request do przejścia na nowy rok szkolny
    /// </summary>
    public class TransitionToNewSchoolYearRequest
    {
        /// <summary>
        /// ID starego roku szkolnego
        /// </summary>
        [Required]
        public string OldSchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// ID nowego roku szkolnego
        /// </summary>
        [Required]
        public string NewSchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// Lista ID szablonów dla nowych zespołów
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "Wymagany jest przynajmniej jeden szablon")]
        public string[] TemplateIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Opcjonalne ustawienia procesu
        /// </summary>
        public SchoolYearProcessOptions? Options { get; set; }
    }

    /// <summary>
    /// Response dla operacji anulowania procesu
    /// </summary>
    public class CancelProcessResponse
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat opisujący wynik operacji
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    #endregion
} 
