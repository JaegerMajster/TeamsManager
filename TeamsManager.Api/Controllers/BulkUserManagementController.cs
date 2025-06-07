using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler zarządzania masowymi operacjami na użytkownikach
    /// Zapewnia RESTful API dla BulkUserManagementOrchestrator
    /// Następuje wzorce z TeamLifecycleController i SchoolYearProcessController
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
        /// Masowy onboarding użytkowników - kompleksowy proces wprowadzania nowych użytkowników
        /// </summary>
        /// <param name="request">Dane żądania onboardingu</param>
        /// <returns>Wynik operacji masowej</returns>
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
        /// Masowy offboarding użytkowników - kompleksowy proces usuwania użytkowników z organizacji
        /// </summary>
        /// <param name="request">Dane żądania offboardingu</param>
        /// <returns>Wynik operacji masowej</returns>
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
        /// Masowa zmiana ról użytkowników w systemie
        /// </summary>
        /// <param name="request">Dane żądania zmiany ról</param>
        /// <returns>Wynik operacji masowej</returns>
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
        /// Masowe operacje członkostwa w zespołach (dodawanie/usuwanie z wielu zespołów)
        /// </summary>
        /// <param name="request">Dane żądania operacji członkostwa</param>
        /// <returns>Wynik operacji masowej</returns>
        [HttpPost("bulk-team-membership")]
        [ProducesResponseType(typeof(BulkTeamMembershipResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> BulkTeamMembershipOperation([FromBody] BulkTeamMembershipRequest request)
        {
            try
            {
                _logger.LogInformation("✅ API: Rozpoczynam masowe operacje członkostwa {Count} operacji", 
                    request.Operations?.Length ?? 0);
                
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
        /// Pobiera status aktualnie wykonywanych procesów zarządzania użytkownikami
        /// </summary>
        /// <returns>Lista statusów aktywnych procesów</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(IEnumerable<UserManagementProcessStatus>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> GetActiveProcessesStatus()
        {
            try
            {
                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                
                _logger.LogInformation("📊 API: Pobrano status {Count} aktywnych procesów zarządzania użytkownikami", 
                    processes.Count());

                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas pobierania statusu procesów zarządzania użytkownikami");
                return StatusCode(500, "Wystąpił błąd podczas pobierania statusu procesów");
            }
        }

        /// <summary>
        /// Anuluje aktywny proces zarządzania użytkownikami (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>Wynik anulowania procesu</returns>
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
                    return BadRequest("ID procesu nie może być pusty");
                }

                var result = await _orchestrator.CancelProcessAsync(processId);

                if (result)
                {
                    _logger.LogInformation("✅ API: Anulowano proces zarządzania użytkownikami {ProcessId}", processId);
                    return Ok(new CancelProcessResponse
                    {
                        Success = true,
                        Message = $"Proces {processId} został anulowany"
                    });
                }
                else
                {
                    _logger.LogWarning("⚠️ API: Nie można anulować procesu {ProcessId} - proces nie istnieje lub nie może być anulowany", processId);
                    return NotFound(new CancelProcessResponse
                    {
                        Success = false,
                        Message = $"Proces {processId} nie istnieje lub nie może być anulowany"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API: Błąd podczas anulowania procesu zarządzania użytkownikami {ProcessId}", processId);
                return StatusCode(500, "Wystąpił błąd podczas anulowania procesu");
            }
        }
    }

    #region Request/Response DTOs

    /// <summary>
    /// Żądanie masowego onboardingu użytkowników
    /// </summary>
    public class BulkUserOnboardingRequest
    {
        /// <summary>
        /// Lista planów onboardingu użytkowników
        /// </summary>
        [Required(ErrorMessage = "Lista planów onboardingu jest wymagana")]
        [MinLength(1, ErrorMessage = "Musi zawierać co najmniej jeden plan onboardingu")]
        public UserOnboardingPlan[]? Plans { get; set; }
    }

    /// <summary>
    /// Odpowiedź masowego onboardingu użytkowników
    /// </summary>
    public class BulkUserOnboardingResponse
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Data przetworzenia żądania
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Łączna liczba planów onboardingu
        /// </summary>
        public int TotalPlans { get; set; }

        /// <summary>
        /// Liczba pomyślnych onboardingów
        /// </summary>
        public int SuccessfulOnboardings { get; set; }

        /// <summary>
        /// Liczba nieudanych onboardingów
        /// </summary>
        public int FailedOnboardings { get; set; }

        /// <summary>
        /// Lista komunikatów błędów
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Żądanie masowego offboardingu użytkowników
    /// </summary>
    public class BulkUserOffboardingRequest
    {
        /// <summary>
        /// Lista ID użytkowników do offboardingu
        /// </summary>
        [Required(ErrorMessage = "Lista ID użytkowników jest wymagana")]
        [MinLength(1, ErrorMessage = "Musi zawierać co najmniej jeden ID użytkownika")]
        public string[]? UserIds { get; set; }

        /// <summary>
        /// Opcje procesu offboardingu
        /// </summary>
        public OffboardingOptions? Options { get; set; }
    }

    /// <summary>
    /// Odpowiedź masowego offboardingu użytkowników
    /// </summary>
    public class BulkUserOffboardingResponse
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Data przetworzenia żądania
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Łączna liczba użytkowników
        /// </summary>
        public int TotalUsers { get; set; }

        /// <summary>
        /// Liczba pomyślnych offboardingów
        /// </summary>
        public int SuccessfulOffboardings { get; set; }

        /// <summary>
        /// Liczba nieudanych offboardingów
        /// </summary>
        public int FailedOffboardings { get; set; }

        /// <summary>
        /// Lista komunikatów błędów
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Żądanie masowej zmiany ról użytkowników
    /// </summary>
    public class BulkRoleChangeRequest
    {
        /// <summary>
        /// Lista zmian ról użytkowników
        /// </summary>
        [Required(ErrorMessage = "Lista zmian ról jest wymagana")]
        [MinLength(1, ErrorMessage = "Musi zawierać co najmniej jedną zmianę roli")]
        public UserRoleChange[]? Changes { get; set; }
    }

    /// <summary>
    /// Odpowiedź masowej zmiany ról użytkowników
    /// </summary>
    public class BulkRoleChangeResponse
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Data przetworzenia żądania
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Łączna liczba zmian ról
        /// </summary>
        public int TotalChanges { get; set; }

        /// <summary>
        /// Liczba pomyślnych zmian ról
        /// </summary>
        public int SuccessfulChanges { get; set; }

        /// <summary>
        /// Liczba nieudanych zmian ról
        /// </summary>
        public int FailedChanges { get; set; }

        /// <summary>
        /// Lista komunikatów błędów
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Żądanie masowych operacji członkostwa w zespołach
    /// </summary>
    public class BulkTeamMembershipRequest
    {
        /// <summary>
        /// Lista operacji członkostwa
        /// </summary>
        [Required(ErrorMessage = "Lista operacji członkostwa jest wymagana")]
        [MinLength(1, ErrorMessage = "Musi zawierać co najmniej jedną operację")]
        public TeamMembershipOperation[]? Operations { get; set; }
    }

    /// <summary>
    /// Odpowiedź masowych operacji członkostwa w zespołach
    /// </summary>
    public class BulkTeamMembershipResponse
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Data przetworzenia żądania
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Łączna liczba operacji
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Liczba pomyślnych operacji
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Liczba nieudanych operacji
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Lista komunikatów błędów
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Odpowiedź dla anulowania procesu
    /// </summary>
    public class CancelProcessResponse
    {
        /// <summary>
        /// Czy anulowanie się powiodło
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat wyniku
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    #endregion
} 