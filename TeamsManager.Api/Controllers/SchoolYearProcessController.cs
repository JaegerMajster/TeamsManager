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
        /// Tworzy zespoły dla nowego roku szkolnego
        /// </summary>
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
        /// Pobiera status aktywnych procesów
        /// </summary>
        [HttpGet("active-processes")]
        [ProducesResponseType(typeof(IEnumerable<SchoolYearProcessStatus>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<SchoolYearProcessStatus>>> GetActiveProcesses()
        {
            try
            {
                _logger.LogInformation("📊 API: Pobieranie statusu aktywnych procesów");
                var processes = await _orchestrator.GetActiveProcessesAsync();
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
                if (string.IsNullOrWhiteSpace(processId))
                {
                    return BadRequest(new CancelProcessResponse 
                    { 
                        Success = false, 
                        Message = "ID procesu jest wymagane" 
                    });
                }

                _logger.LogInformation("🛑 API: Anulowanie procesu {ProcessId}", processId);
                var success = await _orchestrator.CancelProcessAsync(processId);

                if (success)
                {
                    return Ok(new CancelProcessResponse 
                    { 
                        Success = true, 
                        Message = "Proces został anulowany pomyślnie" 
                    });
                }
                else
                {
                    return NotFound(new CancelProcessResponse 
                    { 
                        Success = false, 
                        Message = "Proces nie został znaleziony lub już się zakończył" 
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

    public class CreateTeamsForNewSchoolYearRequest
    {
        [Required]
        public string SchoolYearId { get; set; } = string.Empty;

        [Required]
        public string[] TemplateIds { get; set; } = Array.Empty<string>();

        public SchoolYearProcessOptions? Options { get; set; }
    }

    public class ArchiveTeamsRequest
    {
        [Required]
        public string SchoolYearId { get; set; } = string.Empty;

        public SchoolYearProcessOptions? Options { get; set; }
    }

    public class TransitionToNewSchoolYearRequest
    {
        [Required]
        public string OldSchoolYearId { get; set; } = string.Empty;

        [Required]
        public string NewSchoolYearId { get; set; } = string.Empty;

        [Required]
        public string[] TemplateIds { get; set; } = Array.Empty<string>();

        public SchoolYearProcessOptions? Options { get; set; }
    }

    public class CancelProcessResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 