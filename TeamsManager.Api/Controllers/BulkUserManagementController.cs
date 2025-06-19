using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler zarządzania masowymi operacjami na użytkownikach
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BulkUserManagementController : ControllerBase
    {
        private readonly IBulkUserManagementOrchestrator _orchestrator;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<BulkUserManagementController> _logger;

        public BulkUserManagementController(
            IBulkUserManagementOrchestrator orchestrator,
            ITokenManager tokenManager,
            ILogger<BulkUserManagementController> logger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Masowy onboarding użytkowników
        /// </summary>
        [HttpPost("bulk-onboarding")]
        [ProducesResponseType(typeof(BulkUserOnboardingResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> BulkUserOnboarding([FromBody] BulkUserOnboardingRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masowy onboarding {Count} użytkowników", 
                    request.Plans?.Length ?? 0);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

                var result = await _orchestrator.BulkUserOnboardingAsync(
                    request.Plans ?? Array.Empty<UserOnboardingPlan>(),
                    accessToken);

                _logger.LogInformation("✅ API: Masowy onboarding zakończony. Sukces: {Success}, Błędy: {Errors}", 
                    result.SuccessfulOperations.Count, result.Errors.Count);

                return Ok(new BulkUserOnboardingResponse
                {
                    Success = result.Success,
                    ProcessedAt = result.ProcessedAt,
                    TotalPlans = request.Plans?.Length ?? 0,
                    SuccessfulOnboardings = result.SuccessfulOperations.Count,
                    FailedOnboardings = result.Errors.Count,
                    Errors = result.Errors.Select(e => e.Message).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas masowego onboardingu użytkowników");
                return StatusCode(500, "Wystąpił błąd podczas masowego onboardingu użytkowników");
            }
        }

        /// <summary>
        /// Masowy offboarding użytkowników
        /// </summary>
        [HttpPost("bulk-offboarding")]
        [ProducesResponseType(typeof(BulkUserOffboardingResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> BulkUserOffboarding([FromBody] BulkUserOffboardingRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masowy offboarding {Count} użytkowników", 
                    request.UserIds?.Length ?? 0);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

                var result = await _orchestrator.BulkUserOffboardingAsync(
                    request.UserIds ?? Array.Empty<string>(),
                    request.Options ?? new OffboardingOptions(),
                    accessToken);

                _logger.LogInformation("✅ API: Masowy offboarding zakończony. Sukces: {Success}, Błędy: {Errors}", 
                    result.SuccessfulOperations.Count, result.Errors.Count);

                return Ok(new BulkUserOffboardingResponse
                {
                    Success = result.Success,
                    ProcessedAt = result.ProcessedAt,
                    TotalUsers = request.UserIds?.Length ?? 0,
                    SuccessfulOffboardings = result.SuccessfulOperations.Count,
                    FailedOffboardings = result.Errors.Count,
                    Errors = result.Errors.Select(e => e.Message).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas masowego offboardingu użytkowników");
                return StatusCode(500, "Wystąpił błąd podczas masowego offboardingu użytkowników");
            }
        }

        /// <summary>
        /// Masowa zmiana ról użytkowników
        /// </summary>
        [HttpPost("bulk-role-change")]
        [ProducesResponseType(typeof(BulkRoleChangeResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> BulkRoleChange([FromBody] BulkRoleChangeRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masową zmianę ról {Count} użytkowników", 
                    request.Changes?.Length ?? 0);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

                var result = await _orchestrator.BulkRoleChangeAsync(
                    request.Changes ?? Array.Empty<UserRoleChange>(),
                    accessToken);

                _logger.LogInformation("✅ API: Masowa zmiana ról zakończona. Sukces: {Success}, Błędy: {Errors}", 
                    result.SuccessfulOperations.Count, result.Errors.Count);

                return Ok(new BulkRoleChangeResponse
                {
                    Success = result.Success,
                    ProcessedAt = result.ProcessedAt,
                    TotalChanges = request.Changes?.Length ?? 0,
                    SuccessfulChanges = result.SuccessfulOperations.Count,
                    FailedChanges = result.Errors.Count,
                    Errors = result.Errors.Select(e => e.Message).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas masowej zmiany ról użytkowników");
                return StatusCode(500, "Wystąpił błąd podczas masowej zmiany ról użytkowników");
            }
        }

        /// <summary>
        /// Masowe operacje członkostwa w zespołach
        /// </summary>
        [HttpPost("bulk-team-membership")]
        [ProducesResponseType(typeof(BulkTeamMembershipResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> BulkTeamMembershipOperation([FromBody] BulkTeamMembershipRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masowe operacje członkostwa {Count} użytkowników", 
                    request.Operations?.Length ?? 0);
                
                var authHeader = HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

                var result = await _orchestrator.BulkTeamMembershipOperationAsync(
                    request.Operations ?? Array.Empty<TeamMembershipOperation>(),
                    accessToken);

                _logger.LogInformation("✅ API: Masowe operacje członkostwa zakończone. Sukces: {Success}, Błędy: {Errors}", 
                    result.SuccessfulOperations.Count, result.Errors.Count);

                return Ok(new BulkTeamMembershipResponse
                {
                    Success = result.Success,
                    ProcessedAt = result.ProcessedAt,
                    TotalOperations = request.Operations?.Length ?? 0,
                    SuccessfulOperations = result.SuccessfulOperations.Count,
                    FailedOperations = result.Errors.Count,
                    Errors = result.Errors.Select(e => e.Message).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas masowych operacji członkostwa w zespołach");
                return StatusCode(500, "Wystąpił błąd podczas masowych operacji członkostwa w zespołach");
            }
        }

        /// <summary>
        /// Status aktywnych procesów
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(IEnumerable<UserManagementProcessStatus>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> GetActiveProcessesStatus()
        {
            try
            {
                _logger.LogInformation("📊 API: Pobieranie statusu aktywnych procesów zarządzania użytkownikami");
                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas pobierania statusu procesów");
                return StatusCode(500, "Wystąpił błąd podczas pobierania statusu procesów");
            }
        }

        /// <summary>
        /// Anulowanie procesu
        /// </summary>
        [HttpDelete("{processId}")]
        [ProducesResponseType(typeof(CancelProcessResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> CancelProcess([FromRoute] string processId)
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

                _logger.LogInformation("🛑 API: Anulowanie procesu zarządzania użytkownikami {ProcessId}", processId);
                var success = await _orchestrator.CancelProcessAsync(processId);

                if (success)
                {
                    _logger.LogInformation("✅ API: Proces {ProcessId} anulowany pomyślnie", processId);
                    return Ok(new CancelProcessResponse 
                    { 
                        Success = true, 
                        Message = "Proces został anulowany pomyślnie" 
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Nie udało się anulować procesu {ProcessId}", processId);
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

    public class BulkUserOnboardingRequest
    {
        [Required(ErrorMessage = "Lista planów onboardingu jest wymagana")]
        public UserOnboardingPlan[]? Plans { get; set; }
    }

    public class BulkUserOnboardingResponse
    {
        public bool Success { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int TotalPlans { get; set; }
        public int SuccessfulOnboardings { get; set; }
        public int FailedOnboardings { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class BulkUserOffboardingRequest
    {
        [Required(ErrorMessage = "Lista ID użytkowników jest wymagana")]
        public string[]? UserIds { get; set; }
        public OffboardingOptions? Options { get; set; }
    }

    public class BulkUserOffboardingResponse
    {
        public bool Success { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int TotalUsers { get; set; }
        public int SuccessfulOffboardings { get; set; }
        public int FailedOffboardings { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class BulkRoleChangeRequest
    {
        [Required(ErrorMessage = "Lista zmian ról jest wymagana")]
        public UserRoleChange[]? Changes { get; set; }
    }

    public class BulkRoleChangeResponse
    {
        public bool Success { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int TotalChanges { get; set; }
        public int SuccessfulChanges { get; set; }
        public int FailedChanges { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class BulkTeamMembershipRequest
    {
        [Required(ErrorMessage = "Lista operacji członkostwa jest wymagana")]
        public TeamMembershipOperation[]? Operations { get; set; }
    }

    public class BulkTeamMembershipResponse
    {
        public bool Success { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class CancelProcessResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 